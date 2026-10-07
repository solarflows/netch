using System.Runtime.InteropServices;
using Microsoft.Win32;
using Netch.Utils;

namespace Netch.Services;

public static class ThemeService
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static readonly Color DarkBg = Color.FromArgb(32, 32, 32);
    public static readonly Color DarkCard = Color.FromArgb(45, 45, 45);
    public static readonly Color DarkInput = Color.FromArgb(55, 55, 55);
    public static readonly Color DarkBorder = Color.FromArgb(75, 75, 75);
    public static readonly Color DarkText = Color.FromArgb(240, 240, 240);
    public static readonly Color DarkTextDim = Color.FromArgb(170, 170, 170);

    private static readonly DarkThemeColorTable DarkColors = new();
    private static readonly ToolStripRenderer DarkRenderer = new ToolStripProfessionalRenderer(DarkColors);
    private static readonly ToolStripRenderer DefaultRenderer = new ToolStripProfessionalRenderer();

    static ThemeService()
    {
        SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            if (string.Equals(Global.Settings.Theme, "System", StringComparison.OrdinalIgnoreCase))
            {
                ApplyToOpenForms();
            }
        };
    }

    public static bool IsDarkMode
    {
        get
        {
            var theme = Global.Settings.Theme;
            if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase))
                return false;

            return IsSystemDarkMode();
        }
    }

    public static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int lightMode)
                return lightMode == 0;
        }
        catch
        {
            // ignored
        }

        return false;
    }

    public static void SetWindowDarkMode(IntPtr hWnd, bool dark)
    {
        if (hWnd == IntPtr.Zero)
            return;

        int useDarkMode = dark ? 1 : 0;
        int res = DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
        if (res != 0)
        {
            DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
        }
    }

    public static void ApplyToOpenForms()
    {
        foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
        {
            if (form != null && !form.IsDisposed)
            {
                Apply(form);
            }
        }
    }

    public static void Apply(Form form)
    {
        bool dark = IsDarkMode;
        SetWindowDarkMode(form.Handle, dark);

        form.BackColor = dark ? DarkBg : SystemColors.Control;
        form.ForeColor = dark ? DarkText : SystemColors.ControlText;

        ApplyToControls(form.Controls, dark);
    }

    private static void ApplyToControls(Control.ControlCollection controls, bool dark)
    {
        foreach (Control c in controls)
        {
            switch (c)
            {
                case TabControl tc:
                    tc.BackColor = dark ? DarkBg : SystemColors.Control;
                    tc.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    foreach (TabPage page in tc.TabPages)
                    {
                        page.UseVisualStyleBackColor = !dark;
                        page.BackColor = dark ? DarkBg : SystemColors.Control;
                        page.ForeColor = dark ? DarkText : SystemColors.ControlText;
                        ApplyToControls(page.Controls, dark);
                    }
                    break;

                case Forms.ModernTabStrip mts:
                    mts.Invalidate();
                    break;

                case GroupBox gb:
                    gb.BackColor = dark ? (gb.Parent is TabPage or Form ? DarkBg : DarkCard) : Color.Transparent;
                    gb.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    gb.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
                    if (dark)
                    {
                        gb.Paint -= GroupBox_DarkPaint;
                        gb.Paint += GroupBox_DarkPaint;
                    }
                    ApplyToControls(gb.Controls, dark);
                    break;

                case Panel p:
                    p.BackColor = (p.Name == "tabWrapperPanel") ? (dark ? DarkBg : SystemColors.Control) : (dark ? DarkCard : Color.Transparent);
                    p.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    ApplyToControls(p.Controls, dark);
                    break;

                case Button btn:
                    btn.UseVisualStyleBackColor = !dark;
                    btn.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
                    btn.BackColor = dark ? DarkInput : SystemColors.Control;
                    btn.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    if (dark)
                    {
                        btn.FlatAppearance.BorderColor = DarkBorder;
                        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 70);
                        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(35, 35, 35);
                    }
                    break;

                case TextBoxBase tb:
                    tb.BackColor = dark ? DarkInput : SystemColors.Window;
                    tb.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    tb.BorderStyle = dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                    break;

                case ListBox lb:
                    lb.BackColor = dark ? DarkInput : SystemColors.Window;
                    lb.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    lb.BorderStyle = dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                    break;

                case ListView lv:
                    lv.BackColor = dark ? DarkInput : SystemColors.Window;
                    lv.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    lv.BorderStyle = dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                    break;

                case DataGridView dgv:
                    dgv.BackgroundColor = dark ? DarkBg : SystemColors.Window;
                    dgv.GridColor = dark ? DarkBorder : Color.FromArgb(235, 235, 235);
                    dgv.DefaultCellStyle.BackColor = dark ? DarkCard : SystemColors.Window;
                    dgv.DefaultCellStyle.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215);
                    dgv.DefaultCellStyle.SelectionForeColor = Color.White;

                    dgv.ColumnHeadersDefaultCellStyle.BackColor = dark ? DarkInput : Color.FromArgb(245, 245, 245);
                    dgv.ColumnHeadersDefaultCellStyle.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = dgv.ColumnHeadersDefaultCellStyle.BackColor;
                    dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = dgv.ColumnHeadersDefaultCellStyle.ForeColor;
                    dgv.EnableHeadersVisualStyles = false;
                    break;

                case ComboBox cb:
                    cb.BackColor = dark ? DarkInput : SystemColors.Window;
                    cb.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    break;

                case Label lbl:
                    if (lbl.Name == "SpeedPictureBox")
                    {
                        lbl.ForeColor = Color.FromArgb(245, 195, 35);
                    }
                    else if (lbl.Name.EndsWith("PictureBox") || lbl.Name == "ServerLabel")
                    {
                        lbl.ForeColor = dark ? DarkText : Color.FromArgb(40, 40, 40);
                    }
                    else
                    {
                        lbl.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    }
                    break;

                case PictureBox pb:
                    pb.BackColor = dark ? DarkCard : Color.Transparent;
                    break;

                case ContextMenuStrip cms:
                    cms.BackColor = dark ? DarkCard : SystemColors.Control;
                    cms.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    cms.Renderer = dark ? DarkRenderer : DefaultRenderer;
                    ApplyToToolStripItems(cms.Items, dark);
                    break;

                case ToolStrip ts:
                    ts.BackColor = dark ? DarkBg : SystemColors.Control;
                    ts.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    ts.Renderer = dark ? DarkRenderer : DefaultRenderer;
                    ApplyToToolStripItems(ts.Items, dark);
                    break;

                default:
                    c.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    if (c.HasChildren)
                    {
                        ApplyToControls(c.Controls, dark);
                    }
                    break;
            }
        }
    }

    private static void ApplyToToolStripItems(ToolStripItemCollection items, bool dark)
    {
        foreach (ToolStripItem item in items)
        {
            item.ForeColor = dark ? DarkText : SystemColors.ControlText;

            if (item is ToolStripLabel label)
            {
                if (label.Name == "VersionLabel")
                {
                    label.LinkColor = dark ? Color.FromArgb(100, 185, 255) : Color.FromArgb(0, 102, 204);
                }
            }

            if (item is ToolStripDropDownItem dropDown)
            {
                ApplyToToolStripItems(dropDown.DropDownItems, dark);
            }
        }
    }

    private static void GroupBox_DarkPaint(object? sender, PaintEventArgs e)
    {
        if (sender is not GroupBox gb || !IsDarkMode)
            return;

        var g = e.Graphics;
        var tSize = TextRenderer.MeasureText(gb.Text, gb.Font);
        var borderRect = new Rectangle(0, tSize.Height / 2, gb.Width - 1, gb.Height - tSize.Height / 2 - 1);

        using var bgBrush = new SolidBrush(gb.BackColor);
        g.FillRectangle(bgBrush, gb.ClientRectangle);

        using var borderPen = new Pen(Color.FromArgb(60, 60, 60), 1);
        g.DrawRectangle(borderPen, borderRect);

        if (!string.IsNullOrEmpty(gb.Text))
        {
            var textRect = new Rectangle(8, 0, tSize.Width, tSize.Height);
            g.FillRectangle(bgBrush, textRect);
            TextRenderer.DrawText(g, gb.Text, gb.Font, new Point(8, 0), DarkText);
        }
    }

    private class DarkThemeColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => DarkBg;
        public override Color MenuStripGradientEnd => DarkBg;
        public override Color ToolStripDropDownBackground => DarkCard;
        public override Color ImageMarginGradientBegin => DarkCard;
        public override Color ImageMarginGradientMiddle => DarkCard;
        public override Color ImageMarginGradientEnd => DarkCard;
        public override Color MenuBorder => DarkBorder;
        public override Color MenuItemBorder => DarkBorder;
        public override Color MenuItemSelected => DarkInput;
        public override Color MenuItemSelectedGradientBegin => DarkInput;
        public override Color MenuItemSelectedGradientEnd => DarkInput;
        public override Color MenuItemPressedGradientBegin => DarkCard;
        public override Color MenuItemPressedGradientEnd => DarkCard;
        public override Color StatusStripGradientBegin => DarkBg;
        public override Color StatusStripGradientEnd => DarkBg;
    }
}
