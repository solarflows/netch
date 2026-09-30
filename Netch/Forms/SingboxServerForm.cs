using System.ComponentModel;
using Netch.Models;
using Netch.Properties;
using Netch.Servers;
using Netch.Services;
using Netch.Utils;

namespace Netch.Forms;

[DesignerCategory(@"Code")]
[Fody.ConfigureAwait(true)]
public class SingboxServerForm : Form
{
    private readonly Server? _editingServer;
    private readonly bool _isEditMode;

    private readonly TextBox _remarkTextBox = new();
    private readonly TextBox _addressTextBox = new();
    private readonly TextBox _portTextBox = new();
    private readonly ComboBox _protocolComboBox = new();

    private readonly Panel _dynamicPanel = new();
    private readonly Dictionary<string, Control> _fields = new();

    public SingboxServerForm(Server? server = null)
    {
        _editingServer = server;
        _isEditMode = server != null;

        InitializeLayout();
        LoadServerData();
    }

    private void InitializeLayout()
    {
        Text = _isEditMode ? i18N.Translate("Edit [sing-box] Server") : i18N.Translate("Add [sing-box] Server");
        Icon = Resources.icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(540, 520);
        AutoScroll = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12),
            AutoSize = true
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 1. 顶部友好提示横幅 (Banner)
        var bannerBox = new GroupBox
        {
            Dock = DockStyle.Top,
            Text = i18N.Translate("Notice"),
            AutoSize = true,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 10)
        };
        var bannerLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.FromArgb(0, 120, 215),
            Text = i18N.Translate("💡 Manual configuration only provides essential client parameters.\r\nFor complex routing, chain proxies, or full advanced features, we recommend importing via Subscription URL or Clipboard.")
        };
        bannerBox.Controls.Add(bannerLabel);
        mainLayout.Controls.Add(bannerBox, 0, 0);

        // 2. 基础信息组
        var basicGroup = new GroupBox
        {
            Dock = DockStyle.Top,
            Text = i18N.Translate("Basic Configuration"),
            AutoSize = true,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 10)
        };
        var basicTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            AutoSize = true
        };
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
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
            "WireGuard",
            "Socks5"
        });
        _protocolComboBox.SelectedIndex = 0;
        _protocolComboBox.SelectedIndexChanged += (_, _) => SwitchProtocolFields(_protocolComboBox.SelectedItem?.ToString());

        AddRow(basicTable, 3, i18N.Translate("Protocol"), _protocolComboBox);
        basicGroup.Controls.Add(basicTable);
        mainLayout.Controls.Add(basicGroup, 0, 1);

        // 3. 动态协议参数面板
        _dynamicPanel.Dock = DockStyle.Fill;
        _dynamicPanel.AutoSize = true;
        mainLayout.Controls.Add(_dynamicPanel, 0, 2);

        // 4. 底部按钮
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };
        var cancelBtn = new Button { Text = i18N.Translate("Cancel"), Size = new Size(88, 30), DialogResult = DialogResult.Cancel };
        var saveBtn = new Button { Text = i18N.Translate("Save"), Size = new Size(88, 30) };
        saveBtn.Click += SaveButton_Click;

        btnPanel.Controls.Add(cancelBtn);
        btnPanel.Controls.Add(saveBtn);
        mainLayout.Controls.Add(btnPanel, 0, 3);

        Controls.Add(mainLayout);
        SwitchProtocolFields(_protocolComboBox.SelectedItem?.ToString());

        Load += (_, _) => ThemeService.Apply(this);
    }

    private static void AddRow(TableLayoutPanel table, int row, string labelText, Control input)
    {
        var lbl = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 6) };
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(3, 3, 3, 3);
        table.Controls.Add(lbl, 0, row);
        table.Controls.Add(input, 1, row);
    }

    private void SwitchProtocolFields(string? protocol)
    {
        _dynamicPanel.SuspendLayout();
        _dynamicPanel.Controls.Clear();
        _fields.Clear();

        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = i18N.TranslateFormat("{0} Options", protocol ?? ""),
            AutoSize = true,
            Padding = new Padding(10)
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
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
                var netBox = CreateCombo("Network", new[] { "ws", "grpc", "http", "tcp" }, "ws");
                var hostBox = CreateField("Host", "");
                var pathBox = CreateField("Path", "/");
                var tlsBox = CreateCombo("TLSSecure", new[] { "none", "tls" }, "tls");

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
                var methodBox = CreateCombo("Method", new[] { "2022-blake3-aes-128-gcm", "2022-blake3-aes-256-gcm", "aes-256-gcm", "aes-128-gcm", "chacha20-poly1305" }, "2022-blake3-aes-128-gcm");
                var passBox = CreateField("Password", "");
                var pluginBox = CreateField("Plugin", "");
                var pluginOptBox = CreateField("PluginOption", "");

                AddRow(table, row++, i18N.Translate("Encrypt Method"), methodBox);
                AddRow(table, row++, i18N.Translate("Password"), passBox);
                AddRow(table, row++, i18N.Translate("Plugin"), pluginBox);
                AddRow(table, row++, i18N.Translate("Plugin Options"), pluginOptBox);
                break;
            }

            case "WireGuard":
            {
                var privKeyBox = CreateField("PrivateKey", "");
                var peerPubKeyBox = CreateField("PeerPublicKey", "");
                var localAddrBox = CreateField("LocalAddress", "172.16.0.2/32");
                var mtuBox = CreateField("MTU", "1420");
                var reservedBox = CreateField("Reserved", "0,0,0");
                var pskBox = CreateField("PreSharedKey", "");

                AddRow(table, row++, i18N.Translate("Private Key"), privKeyBox);
                AddRow(table, row++, i18N.Translate("Public Key"), peerPubKeyBox);
                AddRow(table, row++, i18N.Translate("Local Addresses"), localAddrBox);
                AddRow(table, row++, i18N.Translate("MTU"), mtuBox);
                AddRow(table, row++, i18N.Translate("Reserved"), reservedBox);
                AddRow(table, row++, i18N.Translate("PSK"), pskBox);
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

        group.Controls.Add(table);
        _dynamicPanel.Controls.Add(group);
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

            case WireGuardServer wg:
                _protocolComboBox.SelectedItem = "WireGuard";
                SetFieldValue("PrivateKey", wg.PrivateKey);
                SetFieldValue("PeerPublicKey", wg.PeerPublicKey);
                SetFieldValue("LocalAddress", wg.LocalAddresses);
                SetFieldValue("MTU", wg.MTU.ToString());
                SetFieldValue("Reserved", wg.Reserved);
                SetFieldValue("PreSharedKey", wg.PreSharedKey);
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

            case "WireGuard":
                resultServer = new WireGuardServer
                {
                    PrivateKey = GetFieldValue("PrivateKey"),
                    PeerPublicKey = GetFieldValue("PeerPublicKey"),
                    LocalAddresses = GetFieldValue("LocalAddress").ValueOrDefault() ?? "172.16.0.2/32",
                    MTU = int.TryParse(GetFieldValue("MTU"), out var mtu) ? mtu : 1420,
                    Reserved = GetFieldValue("Reserved").ValueOrDefault() ?? "0,0,0",
                    PreSharedKey = GetFieldValue("PreSharedKey").ValueOrDefault()
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
        resultServer.Group = "sing-box";

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
