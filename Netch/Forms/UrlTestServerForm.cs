using System.ComponentModel;
using Netch.Models;
using Netch.Properties;
using Netch.Servers;
using Netch.Services;
using Netch.Utils;

namespace Netch.Forms;

[DesignerCategory(@"Code")]
[Fody.ConfigureAwait(true)]
public class UrlTestServerForm : Form
{
    private readonly UrlTestServer? _editingServer;
    private readonly bool _isEditMode;

    private readonly TextBox _remarkTextBox = new();
    private readonly TextBox _urlTextBox = new();
    private readonly ComboBox _intervalComboBox = new();
    private readonly TextBox _toleranceTextBox = new();
    private readonly ComboBox _idleTimeoutComboBox = new();
    private readonly CheckBox _interruptCheckBox = new();
    private readonly CheckedListBox _nodesCheckedListBox = new();

    private readonly List<Server> _availableServers = new();

    public UrlTestServerForm(UrlTestServer? server = null)
    {
        _editingServer = server;
        _isEditMode = server != null;

        InitializeLayout();
        LoadServerData();
    }

    private void InitializeLayout()
    {
        Text = _isEditMode ? i18N.Translate("Edit [sing-box URLTest] Server") : i18N.Translate("Add [sing-box URLTest] Server");
        Icon = Resources.icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(580, 600);

        // 1. 底部常驻操作栏 (永远吸底)
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

        // 提示横幅
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
            MaximumSize = new Size(520, 0),
            ForeColor = Color.FromArgb(0, 120, 215),
            Text = i18N.Translate("💡 sing-box URLTest 出站能对一组候选节点定期测速并自动切换至最低延迟节点，内置容差防抖机制。\r\n候选节点池中包含的 Socks5 裸节点将自动转为 sing-box 原生出站套壳运行，无需繁琐设置。")
        };
        bannerBox.Controls.Add(bannerLabel);
        mainContentTable.Controls.Add(bannerBox, 0, 0);

        // 基础参数小标题
        var basicHeader = CreateSectionHeader(i18N.Translate("URLTest 参数配置 (官方默认值)"));
        mainContentTable.Controls.Add(basicHeader, 0, 1);

        // 基础参数表单
        var basicTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 6,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 14)
        };
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _remarkTextBox.Text = "sing-box 自动优选 (URLTest)";
        AddRow(basicTable, 0, i18N.Translate("Remark"), _remarkTextBox);

        _urlTextBox.Text = "https://www.gstatic.com/generate_204";
        AddRow(basicTable, 1, i18N.Translate("Test URL"), _urlTextBox);

        _intervalComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _intervalComboBox.Items.AddRange(new object[] { "1m", "3m", "5m", "10m", "15m" });
        _intervalComboBox.SelectedItem = "3m";
        AddRow(basicTable, 2, i18N.Translate("Interval (间隔)"), _intervalComboBox);

        _toleranceTextBox.Text = "50";
        AddRow(basicTable, 3, i18N.Translate("Tolerance (容差/ms)"), _toleranceTextBox);

        _idleTimeoutComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _idleTimeoutComboBox.Items.AddRange(new object[] { "10m", "30m", "1h", "2h" });
        _idleTimeoutComboBox.SelectedItem = "30m";
        AddRow(basicTable, 4, i18N.Translate("Idle Timeout (空闲超时)"), _idleTimeoutComboBox);

        _interruptCheckBox.Text = i18N.Translate("节点切换时中断现有连接 (interrupt_exist_connections)");
        _interruptCheckBox.AutoSize = true;
        _interruptCheckBox.Checked = false;
        AddRow(basicTable, 5, i18N.Translate("Interrupt Connections"), _interruptCheckBox);

        mainContentTable.Controls.Add(basicTable, 0, 2);

        // 候选节点池小标题与快捷操作栏
        var poolHeaderFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 6)
        };
        var poolLabel = CreateSectionHeader(i18N.Translate("候选节点池 (勾选参与测速优选的节点)"));
        var selectAllBtn = new Button { Text = i18N.Translate("全选"), Size = new Size(58, 26), Margin = new Padding(12, 2, 4, 2) };
        var clearBtn = new Button { Text = i18N.Translate("清空"), Size = new Size(58, 26), Margin = new Padding(4, 2, 4, 2) };
        var onlySocksBtn = new Button { Text = i18N.Translate("仅选 Socks5"), Size = new Size(88, 26), Margin = new Padding(4, 2, 4, 2) };

        selectAllBtn.Click += (_, _) => SetAllCheckState(true);
        clearBtn.Click += (_, _) => SetAllCheckState(false);
        onlySocksBtn.Click += (_, _) => SelectOnlySocks5();

        poolHeaderFlow.Controls.Add(poolLabel);
        poolHeaderFlow.Controls.Add(selectAllBtn);
        poolHeaderFlow.Controls.Add(clearBtn);
        poolHeaderFlow.Controls.Add(onlySocksBtn);
        mainContentTable.Controls.Add(poolHeaderFlow, 0, 3);

        // 候选节点多选列表
        _nodesCheckedListBox.Dock = DockStyle.Top;
        _nodesCheckedListBox.Height = 160;
        _nodesCheckedListBox.CheckOnClick = true;
        _nodesCheckedListBox.Margin = new Padding(0, 0, 0, 14);

        mainContentTable.Controls.Add(_nodesCheckedListBox, 0, 4);

        scrollPanel.Controls.Add(mainContentTable);

        Controls.Add(scrollPanel);
        Controls.Add(bottomPanel);

        PopulateAvailableNodes();

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

    private void PopulateAvailableNodes()
    {
        _nodesCheckedListBox.Items.Clear();
        _availableServers.Clear();

        // 列出所有非 URLTest 类型的有效节点
        foreach (var s in Global.Settings.Server)
        {
            if (s is not UrlTestServer && !string.IsNullOrWhiteSpace(s.Remark))
            {
                _availableServers.Add(s);
                bool isChecked = _editingServer != null && _editingServer.Outbounds.Contains(s.Remark);
                _nodesCheckedListBox.Items.Add($"[{s.Type}] {s.Remark} ({s.Hostname}:{s.Port})", isChecked);
            }
        }
    }

    private void SetAllCheckState(bool check)
    {
        for (int i = 0; i < _nodesCheckedListBox.Items.Count; i++)
        {
            _nodesCheckedListBox.SetItemChecked(i, check);
        }
    }

    private void SelectOnlySocks5()
    {
        for (int i = 0; i < _availableServers.Count; i++)
        {
            bool isSocks = _availableServers[i] is Socks5Server;
            _nodesCheckedListBox.SetItemChecked(i, isSocks);
        }
    }

    private void LoadServerData()
    {
        if (_editingServer == null)
            return;

        _remarkTextBox.Text = _editingServer.Remark;
        _urlTextBox.Text = _editingServer.Url;
        _intervalComboBox.SelectedItem = _editingServer.Interval;
        _toleranceTextBox.Text = _editingServer.Tolerance.ToString();
        _idleTimeoutComboBox.SelectedItem = _editingServer.IdleTimeout;
        _interruptCheckBox.Checked = _editingServer.InterruptExistConnections;
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        var remark = _remarkTextBox.Text.Trim();
        if (string.IsNullOrEmpty(remark))
        {
            MessageBoxX.Show(i18N.Translate("Remark cannot be empty"));
            return;
        }

        var selectedOutbounds = new List<string>();
        for (int i = 0; i < _nodesCheckedListBox.CheckedIndices.Count; i++)
        {
            int index = _nodesCheckedListBox.CheckedIndices[i];
            if (index >= 0 && index < _availableServers.Count)
            {
                selectedOutbounds.Add(_availableServers[index].Remark);
            }
        }

        if (selectedOutbounds.Count == 0)
        {
            MessageBoxX.Show(i18N.Translate("Please select at least one candidate node for URLTest"));
            return;
        }

        int tolerance = 50;
        int.TryParse(_toleranceTextBox.Text.Trim(), out tolerance);

        var resultServer = new UrlTestServer
        {
            Remark = remark,
            Url = string.IsNullOrWhiteSpace(_urlTextBox.Text) ? "https://www.gstatic.com/generate_204" : _urlTextBox.Text.Trim(),
            Interval = _intervalComboBox.SelectedItem?.ToString() ?? "3m",
            Tolerance = tolerance > 0 ? tolerance : 50,
            IdleTimeout = _idleTimeoutComboBox.SelectedItem?.ToString() ?? "30m",
            InterruptExistConnections = _interruptCheckBox.Checked,
            Outbounds = selectedOutbounds
        };

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
