# Netch UI 主题系统 (ThemeService) 与多语言 (i18n) 开发规范文档
# UI, Theme & Internationalization Architecture Specification

> 本文档规范了 Netch 在 Windows Forms 下的**深色/浅色主题系统渲染机制**、**复杂自定义控件 (如 DataGridView / ComboBox) 样式规范**以及**多语言国际化 (i18n) 增补流程**。
> 旨在彻底根除“暗色模式下白底白字/发虚不可见”、“新增界面未汉化回退为英文”、“自绘控件闪烁迟滞”等历史高发缺陷。

---

## 1. 主题系统 (ThemeService) 核心工作机理

Netch 的主题系统由 `Netch/Services/ThemeService.cs` 统一调度：

### 1.1 顶层窗口沉浸式深色模式 (DWM)
通过调用 Windows 底层 `dwmapi.dll` 的 `DwmSetWindowAttribute`：
- `DWMWA_USE_IMMERSIVE_DARK_MODE` (属性 20，Windows 10 20H1+ 及 Win11)
- `DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1` (属性 19，Windows 10 1809~1909)
使窗体标题栏、关闭按钮、最大化/最小化区域直接渲染为 Windows 官方沉浸式深色标题栏。

### 1.2 颜色常数基线 (Color Palette)
- **`DarkBg`**：`#202020` (RGB: 32, 32, 32) —— 最底层背景底色
- **`DarkCard`**：`#2D2D2D` (RGB: 45, 45, 45) —— 容器/卡片/表格常规底色
- **`DarkInput`**：`#373737` (RGB: 55, 55, 55) —— 输入框、下拉框、表头底色
- **`DarkBorder`**：`#4B4B4B` (RGB: 75, 75, 75) —— 边界与网格分割线色
- **`DarkText`**：`#F0F0F0` (RGB: 240, 240, 240) —— 主文本高对比度纯白前景色
- **`DarkTextDim`**：`#AAAAAA` (RGB: 170, 170, 170) —— 次要/灰色/未测试文本颜色

---

## 2. DataGridView 深色模式样式踩坑与强制铁律

### 2.1 “白底白字发虚”根因
在 WinForms 中，若仅调用 `ThemeService.Apply(form)`：
- `DataGridView` 若未被 `ThemeService` 显式接管，会进入默认分支，其前景色被强行赋予 `DarkText` (接近纯白色)；
- 但是 `DataGridView` 的 `BackgroundColor` 与 `DefaultCellStyle.BackColor` 依然保留系统默认的纯白或浅灰色；
- **直接导致“白底白字”，肉眼完全不可见！**

### 2.2 规范代码模版
凡是包含 `DataGridView` 的窗体，必须在初始化后执行如下完整样式配置，或交由 `ThemeService` 统一递归处理：
```csharp
bool dark = ThemeService.IsDarkMode;

// 1. 禁用系统自带主题头，否则列头背景色无法自定义
dgv.EnableHeadersVisualStyles = false;

// 2. 网格与背景色
dgv.BackgroundColor = dark ? ThemeService.DarkBg : SystemColors.Window;
dgv.GridColor = dark ? ThemeService.DarkBorder : Color.FromArgb(235, 235, 235);

// 3. 普通单元格默认样式
dgv.DefaultCellStyle.BackColor = dark ? ThemeService.DarkCard : SystemColors.Window;
dgv.DefaultCellStyle.ForeColor = dark ? ThemeService.DarkText : Color.FromArgb(30, 30, 30);

// 4. 选中单元格高亮
dgv.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(0, 95, 185) : Color.FromArgb(0, 120, 215);
dgv.DefaultCellStyle.SelectionForeColor = Color.White;

// 5. 列标题头样式
dgv.ColumnHeadersDefaultCellStyle.BackColor = dark ? ThemeService.DarkInput : Color.FromArgb(242, 242, 242);
dgv.ColumnHeadersDefaultCellStyle.ForeColor = dark ? ThemeService.DarkText : Color.FromArgb(30, 30, 30);
dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = dgv.ColumnHeadersDefaultCellStyle.BackColor;
dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = dgv.ColumnHeadersDefaultCellStyle.ForeColor;
```

在 `CellFormatting` 事件中，**务必在修改前景色前进行可空检查与非选中行颜色保障**：
```csharp
private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
{
    if (e.RowIndex < 0 || e.RowIndex >= dgv.Rows.Count || e.CellStyle == null)
        return;

    bool dark = ThemeService.IsDarkMode;
    bool isSelected = dgv.Rows[e.RowIndex].Selected;

    if (!isSelected)
    {
        e.CellStyle.BackColor = dark ? ThemeService.DarkCard : SystemColors.Window;
        e.CellStyle.ForeColor = dark ? ThemeService.DarkText : Color.FromArgb(30, 30, 30);
    }
}
```

---

## 3. ComboBox 自绘与流畅度保障规范

在拥有 50~200 个大规模节点的场景下，`ComboBox` 的极速渲染遵循以下铁律：

1. **控件双缓冲 (DoubleBuffered)**：
   在窗体加载时通过反射开启 `DoubleBuffered`，消灭刷新时的白色闪烁：
   ```csharp
   typeof(Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance)?
       .SetValue(cbx, true);
   ```
2. **固定行高 (`ItemHeight = 24`)**：
   在 `OwnerDrawFixed` 模式下显式指定固定像素行高，避免 WinForms 内部逐帧调用 GDI 计算高度。
3. **消除绘制阶段 Font 动态测量**：
   `int boxWidth = (int)(cbx.Font.Height * 2.2)` 属于性能杀手，必须提取为 `const int BoxWidth = 56`。
4. **单行无助记符排版**：
   必须开启 `TextFormatFlags.SingleLine | TextFormatFlags.WordEllipsis | TextFormatFlags.NoPrefix`。
5. **展开保护 (DroppedDown Guard)**：
   后台延迟测试更新时，若检测到 `cbx.DroppedDown == true`（用户正在滚动浏览列表），抑制控件级 `Refresh()`，防止下拉弹窗掉帧。

---

## 4. 多语言国际化 (i18n) 增补规范

Netch 的多语言翻译机制并非动态调用翻译 API，而是基于 `Netch/Resources/zh-CN`（以及各语言对应文件）中的静态 JSON 键值对字典。

### 4.1 规范流程
1. **代码中声明翻译键**：
   使用标准且具备自解释性的英文字符串作为 Key，例如：
   ```csharp
   i18N.Translate("Server Manager Panel...")
   i18N.TranslateFormat("Total: {0} servers (Filtered: {1}). Double click row to switch.", total, filtered)
   ```
2. **同步在资源字典中注册**：
   打开 `Netch/Resources/zh-CN`，在末尾添加对应的中文映射：
   ```json
   "Server Manager Panel...": "节点列表与管理面板...",
   "Sort by Delay": "按延迟排序",
   "Sort by Group": "按分组排序",
   "Test Selected": "测试选中",
   "Test All": "测试全部"
   ```
   *注意：若未在 `zh-CN` 中添加词条，`i18N.Translate` 将直接回退输出原英文字符串。*
