using System.Reflection;
using Microsoft.VisualStudio.Threading;
using Netch.Controllers;
using Netch.Enums;
using Netch.Models;
using Netch.Servers;
using Netch.Services;
using Netch.Utils;

namespace Netch.Forms;

[Fody.ConfigureAwait(true)]
public class ServerManagerForm : Form
{
    private readonly DataGridView _grid;
    private readonly TextBox _searchBox;
    private readonly ComboBox _typeFilterBox;
    private readonly Button _sortDelayBtn;
    private readonly Button _pingSelectedBtn;
    private readonly Button _pingAllBtn;
    private readonly Label _statusLabel;
    private readonly ContextMenuStrip _contextMenu;

    private List<Server> _filteredServers = new();

    public ServerManagerForm()
    {
        Text = i18N.Translate("Server Manager") + " - Netch";
        Size = new Size(950, 600);
        MinimumSize = new Size(750, 450);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        // ===== 顶部控制栏 =====
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(10, 8, 10, 8)
        };

        _searchBox = new TextBox
        {
            PlaceholderText = i18N.Translate("Search by remark, host, group..."),
            Width = 240,
            Location = new Point(10, 9)
        };
        _searchBox.TextChanged += (_, _) => ApplyFilter();

        _typeFilterBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 110,
            Location = new Point(260, 8)
        };
        _typeFilterBox.Items.Add(i18N.Translate("All Types"));
        _typeFilterBox.SelectedIndex = 0;
        _typeFilterBox.SelectedIndexChanged += (_, _) => ApplyFilter();

        _sortDelayBtn = new Button
        {
            Text = "⚡ " + i18N.Translate("Sort by Delay"),
            Width = 120,
            Location = new Point(380, 7),
            UseVisualStyleBackColor = true
        };
        _sortDelayBtn.Click += SortByDelay_Click;

        _pingSelectedBtn = new Button
        {
            Text = i18N.Translate("Test Selected"),
            Width = 110,
            Location = new Point(510, 7),
            UseVisualStyleBackColor = true
        };
        _pingSelectedBtn.Click += PingSelected_Click;

        _pingAllBtn = new Button
        {
            Text = i18N.Translate("Test All"),
            Width = 100,
            Location = new Point(630, 7),
            UseVisualStyleBackColor = true
        };
        _pingAllBtn.Click += PingAll_Click;

        topPanel.Controls.AddRange(new Control[]
        {
            _searchBox,
            _typeFilterBox,
            _sortDelayBtn,
            _pingSelectedBtn,
            _pingAllBtn
        });

        // ===== 底部状态栏 =====
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            Padding = new Padding(10, 5, 10, 5)
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = i18N.Translate("Tip: Double click any row to switch server instantly.")
        };
        bottomPanel.Controls.Add(_statusLabel);

        // ===== 中间数据表格 =====
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Color.FromArgb(235, 235, 235)
        };

        // 开启 DataGridView 双缓冲
        typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance)?
            .SetValue(_grid, true);

        SetupColumns();

        // ===== 右键菜单 =====
        _contextMenu = new ContextMenuStrip();
        var switchItem = new ToolStripMenuItem(i18N.Translate("Switch to this server"), null, ContextSwitch_Click);
        var pingItem = new ToolStripMenuItem(i18N.Translate("Test Latency"), null, ContextPing_Click);
        var editItem = new ToolStripMenuItem(i18N.Translate("Edit"), null, ContextEdit_Click);
        var copyItem = new ToolStripMenuItem(i18N.Translate("Copy Link"), null, ContextCopy_Click);
        var deleteItem = new ToolStripMenuItem(i18N.Translate("Delete"), null, ContextDelete_Click);

        _contextMenu.Items.AddRange(new ToolStripItem[]
        {
            switchItem,
            pingItem,
            new ToolStripSeparator(),
            editItem,
            copyItem,
            new ToolStripSeparator(),
            deleteItem
        });
        _grid.ContextMenuStrip = _contextMenu;

        _grid.CellDoubleClick += Grid_CellDoubleClick;
        _grid.CellFormatting += Grid_CellFormatting;

        Controls.Add(_grid);
        Controls.Add(topPanel);
        Controls.Add(bottomPanel);

        LoadTypes();
        RefreshData();

        DelayTestHelper.ServerTested += OnServerTested;
        FormClosing += (_, _) => DelayTestHelper.ServerTested -= OnServerTested;

        ThemeService.Apply(this);
    }

    private void SetupColumns()
    {
        _grid.Columns.Clear();
        _grid.AutoGenerateColumns = false;

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Active",
            HeaderText = "",
            Width = 36,
            Resizable = DataGridViewTriState.False,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Type",
            HeaderText = i18N.Translate("Type"),
            Width = 85,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Remark",
            HeaderText = i18N.Translate("Remark"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Address",
            HeaderText = i18N.Translate("Address"),
            Width = 200,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Delay",
            HeaderText = i18N.Translate("Delay"),
            Width = 85,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Group",
            HeaderText = i18N.Translate("Group"),
            Width = 110,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft }
        });
    }

    private void LoadTypes()
    {
        var types = Global.Settings.Server.Select(s => s.Type).Distinct().OrderBy(t => t);
        foreach (var t in types)
        {
            _typeFilterBox.Items.Add(t);
        }
    }

    private void RefreshData()
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var keyword = _searchBox.Text.Trim();
        var selectedType = _typeFilterBox.SelectedIndex > 0 ? _typeFilterBox.SelectedItem?.ToString() : null;

        var query = Global.Settings.Server.AsEnumerable();

        if (!string.IsNullOrEmpty(selectedType))
        {
            query = query.Where(s => s.Type.Equals(selectedType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(s =>
                (s.Remark?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Hostname?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Group?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        _filteredServers = query.ToList();
        PopulateGrid();
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();

        var activeServer = MainController.Server ?? (Global.Settings.ServerComboBoxSelectedIndex >= 0 && Global.Settings.ServerComboBoxSelectedIndex < Global.Settings.Server.Count
            ? Global.Settings.Server[Global.Settings.ServerComboBoxSelectedIndex]
            : null);

        foreach (var s in _filteredServers)
        {
            bool isActive = s == activeServer;
            int rowIndex = _grid.Rows.Add(
                isActive ? "🟢" : "",
                s.Type,
                s.Remark,
                $"{s.Hostname}:{s.Port}",
                s.Delay >= 0 ? $"{s.Delay} ms" : (s.Delay == -2 ? i18N.Translate("Resolve Failed") : (s.Delay == -4 ? i18N.Translate("Error") : i18N.Translate("Timeout"))),
                s.Group
            );
            _grid.Rows[rowIndex].Tag = s;
        }

        _statusLabel.Text = string.Format(i18N.Translate("Total: {0} servers (Filtered: {1}). Double click row to switch."),
            Global.Settings.Server.Count, _filteredServers.Count);
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
            return;

        var server = _grid.Rows[e.RowIndex].Tag as Server;
        if (server == null)
            return;

        // 延迟列着色
        if (_grid.Columns[e.ColumnIndex].Name == "Delay" && e.CellStyle != null)
        {
            if (server.Delay >= 0)
            {
                e.CellStyle.ForeColor = server.Delay switch
                {
                    > 200 => Color.Red,
                    > 80 => Color.FromArgb(200, 150, 0),
                    _ => Color.FromArgb(16, 124, 65)
                };
                e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
            }
            else
            {
                e.CellStyle.ForeColor = Color.Gray;
            }
        }
    }

    private async void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
            return;

        if (_grid.Rows[e.RowIndex].Tag is Server server)
        {
            await SwitchToServerAsync(server);
        }
    }

    private async Task SwitchToServerAsync(Server server)
    {
        if (Global.MainForm.State == State.Started)
        {
            // 已启动状态：免重启驱动无感热切换！
            _statusLabel.Text = i18N.TranslateFormat("Hot-switching to {0}...", server.Remark);
            try
            {
                await MainController.HotSwitchServerAsync(server);
                _statusLabel.Text = i18N.TranslateFormat("Successfully hot-switched to {0}", server.Remark);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = i18N.Translate("Switch failed: ") + ex.Message;
            }
        }
        else
        {
            // 未启动状态：更新主界面选择
            Global.Settings.ServerComboBoxSelectedIndex = Global.Settings.Server.IndexOf(server);
            Global.MainForm.OnServerHotSwitched(server);
            _statusLabel.Text = i18N.TranslateFormat("Selected {0}", server.Remark);
        }

        PopulateGrid();
    }

    private void OnServerTested(Server server)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        BeginInvoke(() =>
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (_grid.Rows[i].Tag == server)
                {
                    _grid.Rows[i].Cells["Delay"].Value = server.Delay >= 0
                        ? $"{server.Delay} ms"
                        : (server.Delay == -2 ? i18N.Translate("Resolve Failed") : (server.Delay == -4 ? i18N.Translate("Error") : i18N.Translate("Timeout")));
                    break;
                }
            }
        });
    }

    private void SortByDelay_Click(object? sender, EventArgs e)
    {
        _filteredServers = _filteredServers.OrderBy(s => s.Delay < 0 ? int.MaxValue : s.Delay).ToList();
        PopulateGrid();
    }

    private async void PingSelected_Click(object? sender, EventArgs e)
    {
        var selectedServers = _grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.Tag as Server)
            .Where(s => s != null)
            .Cast<Server>()
            .ToList();

        if (selectedServers.Count == 0)
            return;

        _pingSelectedBtn.Enabled = false;
        try
        {
            foreach (var s in selectedServers)
            {
                await DelayTestHelper.TestServerAsync(s);
            }
        }
        finally
        {
            _pingSelectedBtn.Enabled = true;
        }
    }

    private async void PingAll_Click(object? sender, EventArgs e)
    {
        _pingAllBtn.Enabled = false;
        try
        {
            await DelayTestHelper.PerformTestAsync(true);
        }
        finally
        {
            _pingAllBtn.Enabled = true;
        }
    }

    private async void ContextSwitch_Click(object? sender, EventArgs e)
    {
        if (_grid.CurrentRow?.Tag is Server s)
        {
            await SwitchToServerAsync(s);
        }
    }

    private async void ContextPing_Click(object? sender, EventArgs e)
    {
        if (_grid.CurrentRow?.Tag is Server s)
        {
            await DelayTestHelper.TestServerAsync(s);
        }
    }

    private void ContextEdit_Click(object? sender, EventArgs e)
    {
        if (_grid.CurrentRow?.Tag is Server s)
        {
            Hide();
            if (s is UrlTestServer ut)
                new UrlTestServerForm(ut).ShowDialog();
            else if (s is Socks5Server s5 && s5.Group != "Xray" && s5.Group != "sing-box")
                new Socks5Form(s5).ShowDialog();
            else if (s.Group == "sing-box" || s is WireGuardServer || s is SSHServer)
                new SingboxServerForm(s).ShowDialog();
            else
                new XrayServerForm(s).ShowDialog();

            RefreshData();
            Utils.Configuration.SaveAsync().Forget();
            Show();
        }
    }

    private void ContextCopy_Click(object? sender, EventArgs e)
    {
        if (_grid.CurrentRow?.Tag is Server s)
        {
            try
            {
                Clipboard.SetText(ShareLink.GetShareLink(s));
                _statusLabel.Text = i18N.Translate("Copied share link to clipboard");
            }
            catch (Exception ex)
            {
                _statusLabel.Text = ex.Message;
            }
        }
    }

    private async void ContextDelete_Click(object? sender, EventArgs e)
    {
        if (_grid.CurrentRow?.Tag is Server s)
        {
            Global.Settings.Server.Remove(s);
            RefreshData();
            await Utils.Configuration.SaveAsync();
        }
    }
}
