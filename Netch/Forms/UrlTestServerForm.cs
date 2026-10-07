using System.ComponentModel;
using System.Text.RegularExpressions;
using Netch.Models;
using Netch.Properties;
using Netch.Servers;
using Netch.Servers.Singbox;
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

    // PassWall 风格正则与分组筛选控件
    private readonly CheckBox _useCustomFilterCheckBox = new();
    private readonly ComboBox _matchGroupComboBox = new();
    private readonly TextBox _includePatternTextBox = new();
    private readonly TextBox _excludePatternTextBox = new();
    private readonly Label _matchedCountLabel = new();

    private readonly CheckedListBox _nodesCheckedListBox = new();
    private readonly List<Server> _availableServers = new();

    public UrlTestServerForm(UrlTestServer? server = null)
    {
        _editingServer = server;
        _isEditMode = server != null;

        InitializeLayout();
        LoadServerData();
        UpdateFilterPreview();
    }

    private void InitializeLayout()
    {
        Text = _isEditMode ? i18N.Translate("Edit [sing-box URLTest] Server") : i18N.Translate("Add [sing-box URLTest] Server");
        Icon = Resources.icon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(620, 720);

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
            Margin = new Padding(0, 0, 0, 12)
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
            MaximumSize = new Size(560, 0),
            ForeColor = Color.FromArgb(0, 120, 215),
            Text = i18N.Translate("💡 sing-box URLTest 能自动测速并切换至最低延迟节点。支持 PassWall 风格的分组与正则匹配过滤，订阅更新节点后全自动维护！")
        };
        bannerBox.Controls.Add(bannerLabel);
        mainContentTable.Controls.Add(bannerBox, 0, 0);

        // 基础参数配置
        var basicHeader = CreateSectionHeader(i18N.Translate("URLTest 核心参数 (官方规范)"));
        mainContentTable.Controls.Add(basicHeader, 0, 1);

        var basicTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 6,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
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

        // ===== PassWall 风格规则自动筛选板块 =====
        var filterHeader = CreateSectionHeader(i18N.Translate("自动节点规则匹配 (PassWall 模式)"));
        mainContentTable.Controls.Add(filterHeader, 0, 3);

        var filterTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 5,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        filterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _useCustomFilterCheckBox.Text = i18N.Translate("启用规则动态匹配 (订阅更新节点自动生效)");
        _useCustomFilterCheckBox.AutoSize = true;
        _useCustomFilterCheckBox.Checked = true;
        _useCustomFilterCheckBox.CheckedChanged += (_, _) => UpdateFilterPreview();
        AddRow(filterTable, 0, i18N.Translate("Rule Match"), _useCustomFilterCheckBox);

        _matchGroupComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _matchGroupComboBox.Items.Add(i18N.Translate("All Groups"));
        var groups = Global.Settings.Server.Select(s => s.Group).Distinct().OrderBy(g => g);
        foreach (var g in groups)
        {
            _matchGroupComboBox.Items.Add(g);
        }
        _matchGroupComboBox.SelectedIndex = 0;
        _matchGroupComboBox.SelectedIndexChanged += (_, _) => UpdateFilterPreview();
        AddRow(filterTable, 1, i18N.Translate("Match Group (匹配分组)"), _matchGroupComboBox);

        _includePatternTextBox.PlaceholderText = "如: 香港|HK|日本|JP (留空表示不限)";
        _includePatternTextBox.TextChanged += (_, _) => UpdateFilterPreview();
        AddRow(filterTable, 2, i18N.Translate("Include Pattern (包含正则)"), _includePatternTextBox);

        _excludePatternTextBox.Text = "官网|到期|重置|剩余|流量|频道|公告";
        _excludePatternTextBox.PlaceholderText = "如: 官网|到期|重置|剩余|流量";
        _excludePatternTextBox.TextChanged += (_, _) => UpdateFilterPreview();
        AddRow(filterTable, 3, i18N.Translate("Exclude Pattern (排除正则)"), _excludePatternTextBox);

        _matchedCountLabel.AutoSize = true;
        _matchedCountLabel.Font = new Font(Control.DefaultFont, FontStyle.Bold);
        _matchedCountLabel.ForeColor = Color.FromArgb(16, 124, 65);
        _matchedCountLabel.Text = i18N.Translate("实时匹配计算中...");
        AddRow(filterTable, 4, i18N.Translate("Match Preview (匹配预览)"), _matchedCountLabel);

        mainContentTable.Controls.Add(filterTable, 0, 4);

        // ===== 候选节点列表展示 =====
        var poolHeaderFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 6)
        };
        var poolLabel = CreateSectionHeader(i18N.Translate("候选节点池预览 (根据规则自动勾选)"));
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
        mainContentTable.Controls.Add(poolHeaderFlow, 0, 5);

        _nodesCheckedListBox.Dock = DockStyle.Top;
        _nodesCheckedListBox.Height = 180;
        _nodesCheckedListBox.CheckOnClick = true;
        PopulateAvailableNodes();
        mainContentTable.Controls.Add(_nodesCheckedListBox, 0, 6);

        scrollPanel.Controls.Add(mainContentTable);
        Controls.Add(scrollPanel);
        Controls.Add(bottomPanel);

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

        foreach (var s in Global.Settings.Server)
        {
            if (s is not UrlTestServer && !string.IsNullOrWhiteSpace(s.Remark))
            {
                _availableServers.Add(s);
                bool isChecked = _editingServer != null && _editingServer.Outbounds.Contains(s.Remark);
                _nodesCheckedListBox.Items.Add($"[{s.Type}][{s.Group}] {s.Remark}", isChecked);
            }
        }
    }

    private void UpdateFilterPreview()
    {
        bool useFilter = _useCustomFilterCheckBox.Checked;
        _matchGroupComboBox.Enabled = useFilter;
        _includePatternTextBox.Enabled = useFilter;
        _excludePatternTextBox.Enabled = useFilter;

        if (!useFilter)
        {
            int manualCount = _nodesCheckedListBox.CheckedIndices.Count;
            _matchedCountLabel.Text = string.Format(i18N.Translate("手动勾选模式: 已选择 {0} 个节点"), manualCount);
            _matchedCountLabel.ForeColor = Color.FromArgb(0, 120, 215);
            return;
        }

        var tempObj = new UrlTestServer
        {
            UseCustomFilter = true,
            MatchGroup = _matchGroupComboBox.SelectedIndex > 0 ? _matchGroupComboBox.SelectedItem?.ToString() ?? "" : "",
            IncludePattern = _includePatternTextBox.Text.Trim(),
            ExcludePattern = _excludePatternTextBox.Text.Trim()
        };

        var matched = SingboxConfigUtils.FilterCandidatesByRules(tempObj);

        _matchedCountLabel.Text = string.Format(i18N.Translate("规则匹配命中: {0} / {1} 个节点"), matched.Count, _availableServers.Count);
        _matchedCountLabel.ForeColor = matched.Count > 0 ? Color.FromArgb(16, 124, 65) : Color.Red;

        // 自动同步勾选预览
        var matchedRemarks = new HashSet<string>(matched.Select(m => m.Remark));
        for (int i = 0; i < _availableServers.Count; i++)
        {
            _nodesCheckedListBox.SetItemChecked(i, matchedRemarks.Contains(_availableServers[i].Remark));
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

        _useCustomFilterCheckBox.Checked = _editingServer.UseCustomFilter;
        if (!string.IsNullOrWhiteSpace(_editingServer.MatchGroup))
        {
            var idx = _matchGroupComboBox.Items.IndexOf(_editingServer.MatchGroup);
            if (idx >= 0)
                _matchGroupComboBox.SelectedIndex = idx;
        }

        _includePatternTextBox.Text = _editingServer.IncludePattern;
        _excludePatternTextBox.Text = _editingServer.ExcludePattern;
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
            UseCustomFilter = _useCustomFilterCheckBox.Checked,
            MatchGroup = _matchGroupComboBox.SelectedIndex > 0 ? _matchGroupComboBox.SelectedItem?.ToString() ?? "" : "",
            IncludePattern = _includePatternTextBox.Text.Trim(),
            ExcludePattern = _excludePatternTextBox.Text.Trim(),
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

        await Configuration.SaveAsync();
        DialogResult = DialogResult.OK;
        Close();
    }
}
